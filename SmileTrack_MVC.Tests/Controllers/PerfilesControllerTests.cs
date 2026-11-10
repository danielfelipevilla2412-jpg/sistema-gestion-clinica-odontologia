using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Controllers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using Xunit;

namespace SmileTrack_MVC.Tests.Controllers;

public sealed class PerfilesControllerTests
{
    [Fact]
    public async Task CompletarPerfilPaciente_GuardaDocumentoYFechaEnCuentaYFicha()
    {
        await using var context = CrearContext();
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 12,
            Nombre = "Ana",
            Apellidos = "López",
            Correo = "ana@example.test",
            Contrasena = "hash",
            IdRol = 3,
            Estado = "activo"
        });
        context.Pacientes.Add(new Paciente
        {
            IdPaciente = 30,
            IdUsuario = 12,
            Documento = "TEMP12",
            Nombres = "Ana",
            Apellidos = "López",
            FechaNacimiento = new DateTime(2000, 1, 1),
            Estado = "activo"
        });
        await context.SaveChangesAsync();

        var controller = CrearController(context, 12);
        var model = new CompletarPerfilPacienteViewModel
        {
            TipoDocumento = "CC",
            Documento = "123456789",
            FechaNacimiento = new DateTime(1995, 6, 15)
        };

        var result = await controller.CompletarPerfilPaciente(model, default);

        Assert.IsType<RedirectToActionResult>(result);
        var paciente = await context.Pacientes.SingleAsync(p => p.IdUsuario == 12);
        var usuario = await context.Usuarios.SingleAsync(u => u.IdUsuario == 12);
        Assert.Equal("123456789", paciente.Documento);
        Assert.Equal(new DateTime(1995, 6, 15), paciente.FechaNacimiento);
        Assert.Equal(paciente.FechaNacimiento, usuario.FechaNacimiento);
    }

    [Fact]
    public async Task CompletarPerfilPaciente_RechazaDocumentoDeOtroPaciente()
    {
        await using var context = CrearContext();
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 12,
            Nombre = "Ana",
            Apellidos = "López",
            Correo = "ana@example.test",
            Contrasena = "hash",
            IdRol = 3,
            Estado = "activo"
        });
        context.Pacientes.Add(new Paciente
        {
            IdPaciente = 30,
            IdUsuario = 12,
            Documento = "TEMP12",
            Nombres = "Ana",
            Apellidos = "López",
            FechaNacimiento = new DateTime(2000, 1, 1),
            Estado = "activo"
        });
        context.Pacientes.Add(new Paciente
        {
            IdPaciente = 31,
            IdUsuario = 13,
            Documento = "123456789",
            Nombres = "Luis",
            Apellidos = "Ruiz",
            FechaNacimiento = new DateTime(1990, 1, 1),
            Estado = "activo"
        });
        await context.SaveChangesAsync();

        var controller = CrearController(context, 12);
        var model = new CompletarPerfilPacienteViewModel
        {
            TipoDocumento = "CC",
            Documento = "123456789",
            FechaNacimiento = new DateTime(1995, 6, 15)
        };

        var result = await controller.CompletarPerfilPaciente(model, default);

        Assert.IsType<ViewResult>(result);
        Assert.True(controller.ModelState.ContainsKey(nameof(model.Documento)));
        Assert.Equal("TEMP12", (await context.Pacientes.SingleAsync(p => p.IdUsuario == 12)).Documento);
    }

    [Fact]
    public async Task CompletarPerfilPaciente_RechazaDocumentoConFormatoInvalido()
    {
        await using var context = CrearContext();
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 12,
            Nombre = "Ana",
            Apellidos = "López",
            Correo = "ana@example.test",
            Contrasena = "hash",
            IdRol = 3,
            Estado = "activo"
        });
        context.Pacientes.Add(new Paciente
        {
            IdPaciente = 30,
            IdUsuario = 12,
            Documento = "TEMP12",
            Nombres = "Ana",
            Apellidos = "López",
            FechaNacimiento = new DateTime(2000, 1, 1),
            Estado = "activo"
        });
        await context.SaveChangesAsync();

        var controller = CrearController(context, 12);
        var model = new CompletarPerfilPacienteViewModel
        {
            TipoDocumento = "CC",
            Documento = "ABC123",
            FechaNacimiento = new DateTime(1995, 6, 15)
        };

        var result = await controller.CompletarPerfilPaciente(model, default);

        Assert.IsType<ViewResult>(result);
        Assert.True(controller.ModelState.ContainsKey(nameof(model.Documento)));
        Assert.Equal("TEMP12", (await context.Pacientes.SingleAsync(p => p.IdUsuario == 12)).Documento);
    }

    [Fact]
    public async Task StpacPerfilPaciente_NoTrataDocumentoRealConPrefijoTemporalComoProvisional()
    {
        await using var context = CrearContext();
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 12,
            Nombre = "Ana",
            Apellidos = "López",
            Correo = "ana@example.test",
            Contrasena = "hash",
            IdRol = 3,
            Estado = "activo",
            FechaNacimiento = new DateTime(1995, 6, 15)
        });
        context.Pacientes.Add(new Paciente
        {
            IdPaciente = 30,
            IdUsuario = 12,
            Documento = "TEMPORAL-123",
            Nombres = "Ana",
            Apellidos = "López",
            FechaNacimiento = new DateTime(1995, 6, 15),
            Estado = "activo"
        });
        await context.SaveChangesAsync();

        var result = await CrearController(context, 12).StpacPerfilPaciente();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task StpacPerfilPaciente_SolicitaCompletarFechaDeNacimientoNoVinculadaALaCuenta()
    {
        await using var context = CrearContext();
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 12,
            Nombre = "Ana",
            Apellidos = "López",
            Correo = "ana@example.test",
            Contrasena = "hash",
            IdRol = 3,
            Estado = "activo"
        });
        context.Pacientes.Add(new Paciente
        {
            IdPaciente = 30,
            IdUsuario = 12,
            Documento = "123456789",
            Nombres = "Ana",
            Apellidos = "López",
            FechaNacimiento = new DateTime(2000, 1, 1),
            Estado = "activo"
        });
        await context.SaveChangesAsync();

        var result = await CrearController(context, 12).StpacPerfilPaciente();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PerfilesController.CompletarPerfilPaciente), redirect.ActionName);
    }

    private static PerfilesController CrearController(AppDbContext context, int userId)
    {
        var controller = new PerfilesController(context);
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
            "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
        return controller;
    }

    private static AppDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
