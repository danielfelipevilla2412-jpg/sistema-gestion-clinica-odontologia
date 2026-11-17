using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Controllers.Api;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.Perfiles;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class MobileSolicitudCitasControllerTests
{
    private static AppDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static MobileSolicitudCitasController CrearControlador(AppDbContext db, int? idUsuario = 10)
    {
        var controller = new MobileSolicitudCitasController(
            db,
            new CitaService(db, NullLogger<CitaService>.Instance),
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

    private static int Contar(IEnumerable datos)
    {
        int total = 0;
        foreach (var _ in datos) total++;
        return total;
    }

    [Fact]
    public async Task ObtenerServicios_RetornaSoloActivos()
    {
        await using var db = CrearContexto();
        db.Servicios.AddRange(
            new Servicio { IdServicio = 1, Nombre = "Limpieza", Estado = "activo" },
            new Servicio { IdServicio = 2, Nombre = "Ortodoncia", Estado = "inactivo" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.ObtenerServicios(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var servicios = Assert.IsAssignableFrom<IEnumerable>(ok.Value);
        Assert.Equal(1, Contar(servicios));
    }

    [Fact]
    public async Task Solicitar_SinClaimDeUsuario_RetornaUnauthorized()
    {
        await using var db = CrearContexto();
        var controller = CrearControlador(db, idUsuario: null);

        var result = await controller.Solicitar(
            new SolicitudCitaMovil { IdServicio = 1, FechaHora = DateTime.Now.AddDays(2) },
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Solicitar_PerfilIncompleto_RetornaConflict()
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

        var result = await controller.Solicitar(
            new SolicitudCitaMovil { IdServicio = 1, FechaHora = DateTime.Now.AddDays(2) },
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("PROFILE_INCOMPLETE", conflict.Value!.GetType().GetProperty("code")!.GetValue(conflict.Value));
    }

    [Fact]
    public async Task Solicitar_DatosInvalidos_RetornaBadRequest()
    {
        await using var db = CrearContexto();
        SembrarPerfilCompleto(db);
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Solicitar(
            new SolicitudCitaMovil { IdServicio = 0, FechaHora = DateTime.Now.AddDays(2) },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, await db.Citas.CountAsync());
    }

    [Fact]
    public async Task Solicitar_DatosValidos_RegistraCitaProgramada()
    {
        await using var db = CrearContexto();
        SembrarPerfilCompleto(db);
        var especialidad = new Especialidad { IdEspecialidad = 1, Nombre = "Odontología" };
        var profesional = new Profesional
        {
            IdProfesional = 1,
            Nombres = "Dra. Ana",
            Apellidos = "Dental",
            Estado = "activo"
        };
        db.Especialidades.Add(especialidad);
        db.Servicios.Add(new Servicio
        {
            IdServicio = 1,
            Nombre = "Limpieza",
            Estado = "activo",
            IdEspecialidad = especialidad.IdEspecialidad,
            DuracionMinutos = 60
        });
        db.Profesionales.Add(profesional);
        db.ProfesionalServicios.Add(new ProfesionalServicio
        {
            IdProfesional = profesional.IdProfesional,
            IdServicio = 1,
            Activo = true
        });
        db.ProfesionalEspecialidades.Add(new Profesional_Especialidad
        {
            IdProfesional = profesional.IdProfesional,
            IdEspecialidad = especialidad.IdEspecialidad,
            Principal = true
        });
        db.HorariosProfesional.Add(new HorarioProfesional
        {
            IdProfesional = profesional.IdProfesional,
            DiaSemana = "Lunes",
            HoraInicio = new TimeOnly(8, 0),
            HoraFin = new TimeOnly(12, 0),
            Activo = true
        });
        db.Consultorios.Add(new Consultorio { IdConsultorio = 1, Nombre = "Consultorio 1", Estado = "activo" });
        db.EstadosCita.Add(new EstadoCita { IdEstado = 1, NombreEstado = "Programada" });
        await db.SaveChangesAsync();

        DateTime fecha = ColombiaClock.Today.AddDays(1);
        while (fecha.DayOfWeek != DayOfWeek.Monday)
            fecha = fecha.AddDays(1);
        fecha = fecha.Date.AddHours(9);

        var controller = CrearControlador(db);

        var result = await controller.Solicitar(
            new SolicitudCitaMovil
            {
                IdServicio = 1,
                IdProfesional = profesional.IdProfesional,
                FechaHora = fecha,
                Notas = "Prefiero en la mañana"
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Programada", ok.Value!.GetType().GetProperty("estado")!.GetValue(ok.Value));

        var cita = await db.Citas.SingleAsync();
        Assert.Equal(1, cita.IdPaciente);
        Assert.Equal(1, cita.IdServicio);
        Assert.Equal(profesional.IdProfesional, cita.IdProfesional);
        Assert.Equal("Programada", cita.Estado);
    }
}
