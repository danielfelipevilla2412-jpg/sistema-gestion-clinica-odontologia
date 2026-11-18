using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class UsuarioAdminServiceTests
{
    [Fact]
    public async Task CrearAsync_ConDatosValidos_CreaUsuarioHasheadoYRegistraAuditoria()
    {
        await using var context = CrearContext();
        context.Roles.Add(new Rol { IdRol = 1, NombreRol = "Recepcionista" });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).CrearAsync(new CrearUsuarioAdminRequest
        {
            Nombre = "Nuevo",
            Apellidos = "Usuario",
            Correo = "nuevo@smiletrack.test",
            Contrasena = "Abc12345!",
            IdRol = 1,
            Estado = "activo"
        }, creadoPor: 99);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Data);
        var usuario = await context.Usuarios.SingleAsync();
        Assert.Equal("nuevo@smiletrack.test", usuario.Correo);
        Assert.NotEqual("Abc12345!", usuario.Contrasena);
        Assert.Equal("activo", usuario.Estado);
        Assert.Equal(99, usuario.CreadoPor);
        Assert.Equal(1, await context.Auditorias.CountAsync());
    }

    [Fact]
    public async Task CrearAsync_ContrasenaInvalida_RetornaMensajeDeValidacion()
    {
        await using var context = CrearContext();
        context.Roles.Add(new Rol { IdRol = 1, NombreRol = "Recepcionista" });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).CrearAsync(new CrearUsuarioAdminRequest
        {
            Nombre = "N",
            Apellidos = "U",
            Correo = "x@y.com",
            Contrasena = "corta",
            IdRol = 1
        }, creadoPor: null);

        Assert.False(result.Success);
        Assert.Contains("contraseña", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await context.Usuarios.ToListAsync());
    }

    [Fact]
    public async Task CrearAsync_RolInexistente_RetornaError()
    {
        await using var context = CrearContext();

        var result = await CrearServicio(context).CrearAsync(new CrearUsuarioAdminRequest
        {
            Nombre = "N",
            Apellidos = "U",
            Correo = "x@y.com",
            Contrasena = "Abc12345!",
            IdRol = 999
        }, creadoPor: null);

        Assert.False(result.Success);
        Assert.Contains("rol seleccionado no existe", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrearAsync_RolNoCreable_RetornaErrorDeVinculacion()
    {
        await using var context = CrearContext();
        context.Roles.Add(new Rol { IdRol = 2, NombreRol = "Profesional" });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).CrearAsync(new CrearUsuarioAdminRequest
        {
            Nombre = "N",
            Apellidos = "U",
            Correo = "x@y.com",
            Contrasena = "Abc12345!",
            IdRol = 2
        }, creadoPor: null);

        Assert.False(result.Success);
        Assert.Contains("módulos", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrearAsync_CorreoDuplicado_RetornaError()
    {
        await using var context = CrearContext();
        context.Roles.Add(new Rol { IdRol = 1, NombreRol = "Recepcionista" });
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 1,
            Nombre = "Existente",
            Apellidos = "User",
            Correo = "dup@smiletrack.test",
            Contrasena = "hash",
            IdRol = 1,
            Estado = "activo"
        });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).CrearAsync(new CrearUsuarioAdminRequest
        {
            Nombre = "N",
            Apellidos = "U",
            Correo = "dup@smiletrack.test",
            Contrasena = "Abc12345!",
            IdRol = 1
        }, creadoPor: null);

        Assert.False(result.Success);
        Assert.Contains("correo ya se encuentra registrado", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await context.Usuarios.CountAsync());
    }

    [Fact]
    public async Task ActualizarAsync_UsuarioInexistente_RetornaError()
    {
        await using var context = CrearContext();

        var result = await CrearServicio(context).ActualizarAsync(999, new ActualizarUsuarioAdminRequest
        {
            Nombre = "N",
            Apellidos = "U",
            Correo = "x@y.com",
            IdRol = 1
        }, usuarioOperador: null);

        Assert.False(result.Success);
        Assert.Contains("Usuario no encontrado", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActualizarAsync_NoPermiteAsignarRolAdministrador()
    {
        await using var context = CrearContext();
        context.Roles.AddRange(
            new Rol { IdRol = 1, NombreRol = "Recepcionista" },
            new Rol { IdRol = 9, NombreRol = "Administrador" });
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 1,
            Nombre = "N",
            Apellidos = "U",
            Correo = "u@u.com",
            Contrasena = "hash",
            IdRol = 1,
            Estado = "activo"
        });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).ActualizarAsync(1, new ActualizarUsuarioAdminRequest
        {
            Nombre = "N",
            Apellidos = "U",
            Correo = "u@u.com",
            IdRol = 9
        }, usuarioOperador: null);

        Assert.False(result.Success);
        Assert.Contains("Administrador no se puede asignar", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActualizarAsync_CuentaAdministrador_NoSeModifica()
    {
        await using var context = CrearContext();
        context.Roles.AddRange(
            new Rol { IdRol = 1, NombreRol = "Recepcionista" },
            new Rol { IdRol = 9, NombreRol = "Administrador" });
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 1,
            Nombre = "Admin",
            Apellidos = "Root",
            Correo = "admin@u.com",
            Contrasena = "hash",
            IdRol = 9,
            Estado = "activo"
        });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).ActualizarAsync(1, new ActualizarUsuarioAdminRequest
        {
            Nombre = "Otro",
            Apellidos = "Nombre",
            Correo = "admin@u.com",
            IdRol = 1
        }, usuarioOperador: null);

        Assert.False(result.Success);
        Assert.Contains("Administrador no se modifica", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Admin", (await context.Usuarios.SingleAsync()).Nombre);
    }

    [Fact]
    public async Task ActualizarAsync_DatosValidos_ActualizaUsuario()
    {
        await using var context = CrearContext();
        context.Roles.AddRange(
            new Rol { IdRol = 1, NombreRol = "Recepcionista" },
            new Rol { IdRol = 2, NombreRol = "Auxiliar" });
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 1,
            Nombre = "Viejo",
            Apellidos = "Dato",
            Correo = "viejo@u.com",
            Contrasena = "hash",
            IdRol = 1,
            Estado = "activo"
        });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).ActualizarAsync(1, new ActualizarUsuarioAdminRequest
        {
            Nombre = "Nuevo",
            Apellidos = "Dato",
            Correo = "nuevo@u.com",
            IdRol = 2,
            Estado = "inactivo"
        }, usuarioOperador: 5);

        Assert.True(result.Success, result.Message);
        var usuario = await context.Usuarios.SingleAsync();
        Assert.Equal("Nuevo", usuario.Nombre);
        Assert.Equal("nuevo@u.com", usuario.Correo);
        Assert.Equal(2, usuario.IdRol);
        Assert.Equal("inactivo", usuario.Estado);
    }

    [Fact]
    public async Task CambiarEstadoAsync_IdInvalido_RetornaError()
    {
        await using var context = CrearContext();

        var result = await CrearServicio(context).CambiarEstadoAsync(
            0,
            new CambiarEstadoUsuarioRequest { Estado = "inactivo" },
            usuarioOperador: null);

        Assert.False(result.Success);
        Assert.Contains("inválido", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CambiarEstadoAsync_CuentaAdministrador_NoPermitido()
    {
        await using var context = CrearContext();
        context.Roles.Add(new Rol { IdRol = 9, NombreRol = "Administrador" });
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 1,
            Nombre = "Admin",
            Apellidos = "Root",
            Correo = "a@a.com",
            Contrasena = "hash",
            IdRol = 9,
            Estado = "activo"
        });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).CambiarEstadoAsync(
            1,
            new CambiarEstadoUsuarioRequest { Estado = "inactivo" },
            usuarioOperador: null);

        Assert.False(result.Success);
        Assert.Contains("Administrador no se puede desactivar", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CambiarEstadoAsync_UsuarioNormal_CambiaEstadoPersistido()
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

        var result = await CrearServicio(context).CambiarEstadoAsync(
            1,
            new CambiarEstadoUsuarioRequest { Estado = "inactivo" },
            usuarioOperador: 5);

        Assert.True(result.Success, result.Message);
        Assert.Equal("inactivo", (await context.Usuarios.SingleAsync()).Estado);
    }

    [Fact]
    public async Task CambiarBloqueoAsync_Bloquear_PoneIntentosEnUmbralDelLogin()
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

        var result = await CrearServicio(context).CambiarBloqueoAsync(
            1,
            new CambiarBloqueoUsuarioRequest { Bloquear = true },
            usuarioOperador: 5);

        Assert.True(result.Success, result.Message);
        Assert.Equal(3, (await context.Usuarios.SingleAsync()).IntentosFallidos);
    }

    [Fact]
    public async Task CambiarBloqueoAsync_Desbloquear_ReiniciaIntentos()
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
            Estado = "activo",
            IntentosFallidos = 3
        });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).CambiarBloqueoAsync(
            1,
            new CambiarBloqueoUsuarioRequest { Bloquear = false },
            usuarioOperador: 5);

        Assert.True(result.Success, result.Message);
        Assert.Equal(0, (await context.Usuarios.SingleAsync()).IntentosFallidos);
    }

    [Fact]
    public async Task CambiarBloqueoAsync_CuentaAdministrador_NoPermitido()
    {
        await using var context = CrearContext();
        context.Roles.Add(new Rol { IdRol = 9, NombreRol = "Administrador" });
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 1,
            Nombre = "Admin",
            Apellidos = "Root",
            Correo = "a@a.com",
            Contrasena = "hash",
            IdRol = 9,
            Estado = "activo"
        });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).CambiarBloqueoAsync(
            1,
            new CambiarBloqueoUsuarioRequest { Bloquear = true },
            usuarioOperador: null);

        Assert.False(result.Success);
        Assert.Contains("no se puede bloquear", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AppDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static UsuarioAdminService CrearServicio(AppDbContext context)
        => new(
            context,
            NullLogger<UsuarioAdminService>.Instance,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
}
