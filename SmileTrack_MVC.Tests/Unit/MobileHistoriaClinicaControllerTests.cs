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
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.Perfiles;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class MobileHistoriaClinicaControllerTests
{
    private static AppDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static MobileHistoriaClinicaController CrearControlador(AppDbContext db, int? idUsuario = 10)
    {
        var controller = new MobileHistoriaClinicaController(
            db,
            new AuditoriaHistoriaClinicaService(db),
            new PerfilPacienteService(db));

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

    private static void SembrarPerfilCompleto(AppDbContext db)
    {
        var nacimiento = new DateTime(1990, 5, 5);
        db.Usuarios.Add(new Usuario
        {
            IdUsuario = 10,
            Nombre = "Ana",
            Apellidos = "Prueba",
            Correo = "ana@example.test",
            Contrasena = "hash",
            IdRol = 3,
            Estado = "activo",
            FechaNacimiento = nacimiento
        });
        db.Pacientes.Add(new Paciente
        {
            IdPaciente = 1,
            IdUsuario = 10,
            TipoDocumento = "CC",
            Documento = "1234567",
            Nombres = "Ana",
            Apellidos = "Prueba",
            FechaNacimiento = nacimiento,
            Estado = "activo"
        });
    }

    [Fact]
    public async Task ObtenerHistorial_SinClaimDeUsuario_RetornaUnauthorized()
    {
        await using var db = CrearContexto();
        var controller = CrearControlador(db, idUsuario: null);

        var result = await controller.ObtenerHistorial(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task ObtenerHistorial_PerfilIncompleto_RetornaConflict()
    {
        await using var db = CrearContexto();
        db.Usuarios.Add(new Usuario
        {
            IdUsuario = 10,
            Nombre = "Ana",
            Apellidos = "Prueba",
            Correo = "ana@example.test",
            Contrasena = "hash",
            IdRol = 3,
            Estado = "activo"
        });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.ObtenerHistorial(CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("PROFILE_INCOMPLETE", conflict.Value!.GetType().GetProperty("code")!.GetValue(conflict.Value));
    }

    [Fact]
    public async Task ObtenerHistorial_SinHistorias_RetornaOkVacioSinAuditoria()
    {
        await using var db = CrearContexto();
        SembrarPerfilCompleto(db);
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.ObtenerHistorial(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(0, await db.Auditorias.CountAsync());
    }

    [Fact]
    public async Task ObtenerHistorial_ConHistoria_RetornaOkYRegistraAuditoria()
    {
        await using var db = CrearContexto();
        SembrarPerfilCompleto(db);
        db.HistoriasClinicas.Add(new HistoriaClinica
        {
            IdHistoria = 1,
            IdPaciente = 1,
            FechaApertura = DateTime.Today.AddDays(-1),
            ObservacionesGenerales = "Paciente estable",
            Activa = true
        });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.ObtenerHistorial(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);

        var auditoria = await db.Auditorias.SingleAsync();
        Assert.Equal(10, auditoria.IdUsuario);
        Assert.Equal("SELECT", auditoria.Accion);
        Assert.Equal("Historia_Clinica", auditoria.TablaAfectada);
        Assert.Equal(1, auditoria.IdRegistro);
    }
}
