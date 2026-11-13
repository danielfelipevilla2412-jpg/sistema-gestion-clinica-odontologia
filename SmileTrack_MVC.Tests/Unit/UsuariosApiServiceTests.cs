using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.Administracion;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class UsuariosApiServiceTests
{
    [Fact]
    public async Task ObtenerAsync_AplicaBusquedaYPaginacion()
    {
        await using var context = CrearContext();
        context.Roles.Add(new Rol { IdRol = 1, NombreRol = "Recepcionista" });
        context.Usuarios.AddRange(
            new Usuario { IdUsuario = 1, Nombre = "Ana", Apellidos = "Zapata", Correo = "ana@u.com", Contrasena = "h", IdRol = 1, Estado = "activo" },
            new Usuario { IdUsuario = 2, Nombre = "Ana", Apellidos = "Bello", Correo = "ana2@u.com", Contrasena = "h", IdRol = 1, Estado = "activo" },
            new Usuario { IdUsuario = 3, Nombre = "Beto", Apellidos = "Cruz", Correo = "beto@u.com", Contrasena = "h", IdRol = 1, Estado = "activo" });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).ObtenerAsync("Ana", null, null, 1, 1);

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("Bello", result.Items[0].Apellidos);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
    }

    [Fact]
    public async Task ObtenerAsync_FiltraUsuariosBloqueadosPorIntentosFallidos()
    {
        await using var context = CrearContext();
        context.Roles.Add(new Rol { IdRol = 1, NombreRol = "Recepcionista" });
        context.Usuarios.AddRange(
            new Usuario { IdUsuario = 1, Nombre = "A", Apellidos = "A", Correo = "a@u.com", Contrasena = "h", IdRol = 1, Estado = "activo", IntentosFallidos = 3 },
            new Usuario { IdUsuario = 2, Nombre = "B", Apellidos = "B", Correo = "b@u.com", Contrasena = "h", IdRol = 1, Estado = "activo", IntentosFallidos = 0 });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).ObtenerAsync(null, "bloqueado", null, 1, 20);

        Assert.Equal(1, result.TotalCount);
        Assert.True(result.Items.Single().EstaBloqueado);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_UsuarioInexistente_RetornaNull()
    {
        await using var context = CrearContext();

        var result = await CrearServicio(context).ObtenerPorIdAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_UsuarioExistente_RetornaDtoConRol()
    {
        await using var context = CrearContext();
        context.Roles.Add(new Rol { IdRol = 1, NombreRol = "Auxiliar" });
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 7,
            Nombre = "Carlos",
            Apellidos = "Ruiz",
            Correo = "carlos@u.com",
            Contrasena = "h",
            IdRol = 1,
            Estado = "activo"
        });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).ObtenerPorIdAsync(7);

        Assert.NotNull(result);
        Assert.Equal("Carlos Ruiz", result!.NombreCompleto);
        Assert.Equal("Auxiliar", result.Rol);
    }

    [Fact]
    public async Task CrearAsync_ContrasenaInvalida_Retorna422()
    {
        await using var context = CrearContext();
        context.Roles.Add(new Rol { IdRol = 1, NombreRol = "Recepcionista" });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).CrearAsync(new UsuarioApiCreateDto
        {
            Nombre = "N",
            Apellidos = "U",
            Correo = "x@y.com",
            Contrasena = "corta",
            IdRol = 1
        }, operadorId: null);

        Assert.False(result.Success);
        Assert.Equal(422, result.ErrorStatusCode);
        Assert.Empty(await context.Usuarios.ToListAsync());
    }

    [Fact]
    public async Task CrearAsync_CorreoDuplicado_Retorna409()
    {
        await using var context = CrearContext();
        context.Roles.Add(new Rol { IdRol = 1, NombreRol = "Recepcionista" });
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 1,
            Nombre = "Existente",
            Apellidos = "User",
            Correo = "dup@u.com",
            Contrasena = "h",
            IdRol = 1,
            Estado = "activo"
        });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).CrearAsync(new UsuarioApiCreateDto
        {
            Nombre = "N",
            Apellidos = "U",
            Correo = "dup@u.com",
            Contrasena = "Abc12345!",
            IdRol = 1
        }, operadorId: null);

        Assert.False(result.Success);
        Assert.Equal(409, result.ErrorStatusCode);
    }

    [Fact]
    public async Task CrearAsync_RolInexistente_Retorna404()
    {
        await using var context = CrearContext();

        var result = await CrearServicio(context).CrearAsync(new UsuarioApiCreateDto
        {
            Nombre = "N",
            Apellidos = "U",
            Correo = "x@y.com",
            Contrasena = "Abc12345!",
            IdRol = 999
        }, operadorId: null);

        Assert.False(result.Success);
        Assert.Equal(404, result.ErrorStatusCode);
    }

    [Fact]
    public async Task CrearAsync_RolNoCreable_Retorna422()
    {
        await using var context = CrearContext();
        context.Roles.Add(new Rol { IdRol = 2, NombreRol = "Profesional" });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).CrearAsync(new UsuarioApiCreateDto
        {
            Nombre = "N",
            Apellidos = "U",
            Correo = "x@y.com",
            Contrasena = "Abc12345!",
            IdRol = 2
        }, operadorId: null);

        Assert.False(result.Success);
        Assert.Equal(422, result.ErrorStatusCode);
    }

    [Fact]
    public async Task CrearAsync_DatosValidos_CreaUsuarioNormalizandoCorreo()
    {
        await using var context = CrearContext();
        context.Roles.Add(new Rol { IdRol = 1, NombreRol = "Recepcionista" });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).CrearAsync(new UsuarioApiCreateDto
        {
            Nombre = "Nuevo",
            Apellidos = "User",
            Correo = "NUEVO@SmileTrack.Test",
            Contrasena = "Abc12345!",
            IdRol = 1
        }, operadorId: 5);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal("nuevo@smiletrack.test", result.Data!.Correo);
        Assert.Equal("Recepcionista", result.Data.Rol);
        Assert.Equal(1, await context.Auditorias.CountAsync());
    }

    [Fact]
    public async Task ActualizarAsync_CuentaAdministrador_Retorna403()
    {
        await using var context = CrearContext();
        context.Roles.Add(new Rol { IdRol = 9, NombreRol = "Administrador" });
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 1,
            Nombre = "Admin",
            Apellidos = "Root",
            Correo = "admin@u.com",
            Contrasena = "h",
            IdRol = 9,
            Estado = "activo"
        });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).ActualizarAsync(1, new UsuarioApiUpdateDto
        {
            Nombre = "Otro"
        }, operadorId: null);

        Assert.False(result.Success);
        Assert.Equal(403, result.ErrorStatusCode);
    }

    [Fact]
    public async Task ActualizarAsync_UsuarioInexistente_Retorna404()
    {
        await using var context = CrearContext();

        var result = await CrearServicio(context).ActualizarAsync(999, new UsuarioApiUpdateDto
        {
            Nombre = "X"
        }, operadorId: null);

        Assert.False(result.Success);
        Assert.Equal(404, result.ErrorStatusCode);
    }

    [Fact]
    public async Task RestablecerContrasenaAsync_UsuarioNormal_ActualizaYReiniciaIntentos()
    {
        await using var context = CrearContext();
        context.Roles.Add(new Rol { IdRol = 1, NombreRol = "Recepcionista" });
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 1,
            Nombre = "N",
            Apellidos = "U",
            Correo = "n@u.com",
            Contrasena = "hash-viejo",
            IdRol = 1,
            Estado = "activo",
            IntentosFallidos = 2
        });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).RestablecerContrasenaAsync(1, "Nueva12345!", operadorId: 5);

        Assert.True(result.Success, result.Message);
        var usuario = await context.Usuarios.SingleAsync();
        Assert.Equal(0, usuario.IntentosFallidos);
        Assert.NotEqual("hash-viejo", usuario.Contrasena);
        Assert.NotNull(usuario.UltimoLogout);
    }

    [Fact]
    public async Task RestablecerContrasenaAsync_ContrasenaInvalida_Retorna422()
    {
        await using var context = CrearContext();
        context.Roles.Add(new Rol { IdRol = 1, NombreRol = "Recepcionista" });
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 1,
            Nombre = "N",
            Apellidos = "U",
            Correo = "n@u.com",
            Contrasena = "hash",
            IdRol = 1,
            Estado = "activo"
        });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).RestablecerContrasenaAsync(1, "corta", operadorId: null);

        Assert.False(result.Success);
        Assert.Equal(422, result.ErrorStatusCode);
    }

    [Fact]
    public async Task DesbloquearAsync_UsuarioNoBloqueado_RetornaFallo()
    {
        await using var context = CrearContext();
        context.Roles.Add(new Rol { IdRol = 1, NombreRol = "Recepcionista" });
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 1,
            Nombre = "N",
            Apellidos = "U",
            Correo = "n@u.com",
            Contrasena = "h",
            IdRol = 1,
            Estado = "activo",
            IntentosFallidos = 1
        });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).DesbloquearAsync(1, operadorId: null);

        Assert.False(result.Success);
        Assert.Contains("no se encuentra bloqueado", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DesbloquearAsync_UsuarioBloqueado_ReiniciaIntentos()
    {
        await using var context = CrearContext();
        context.Roles.Add(new Rol { IdRol = 1, NombreRol = "Recepcionista" });
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 1,
            Nombre = "N",
            Apellidos = "U",
            Correo = "n@u.com",
            Contrasena = "h",
            IdRol = 1,
            Estado = "activo",
            IntentosFallidos = 3
        });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).DesbloquearAsync(1, operadorId: 5);

        Assert.True(result.Success, result.Message);
        Assert.Equal(0, (await context.Usuarios.SingleAsync()).IntentosFallidos);
    }

    [Fact]
    public async Task ObtenerRolesAsync_CuentaUsuariosAsignados()
    {
        await using var context = CrearContext();
        context.Roles.AddRange(
            new Rol { IdRol = 1, NombreRol = "Recepcionista", Descripcion = "Recepción" },
            new Rol { IdRol = 2, NombreRol = "Auxiliar" });
        context.Usuarios.AddRange(
            new Usuario { IdUsuario = 1, Nombre = "A", Apellidos = "A", Correo = "a@u.com", Contrasena = "h", IdRol = 1, Estado = "activo" },
            new Usuario { IdUsuario = 2, Nombre = "B", Apellidos = "B", Correo = "b@u.com", Contrasena = "h", IdRol = 1, Estado = "activo" });
        await context.SaveChangesAsync();

        var roles = await CrearServicio(context).ObtenerRolesAsync();

        Assert.Equal(2, roles.Count);
        Assert.Equal("Auxiliar", roles[0].Nombre);
        var recepcionista = roles.Single(r => r.Nombre == "Recepcionista");
        Assert.Equal(2, recepcionista.UsuariosAsignados);
    }

    private static AppDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static UsuariosApiService CrearServicio(AppDbContext context)
        => new(
            context,
            NullLogger<UsuariosApiService>.Instance,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
}
